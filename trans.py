import json
import time
import itertools
import regex as re
from dataclasses import dataclass
import click
from tqdm import tqdm
import requests
from requests.packages.urllib3.util.retry import Retry
import sys

JP_ID_RANGES = [
    (ord("\u4e00"), ord("\u9FFC")), # CJK Unified Ideographs
    (ord("\u3040"), ord("\u309f")), # hirigana
    (ord("\u30a0"), ord("\u30ff")), # katakana
]

def in_range(x, range):
    return x >= range[0] and x <= range[1]

# shitty heuristic lmao
def is_char_jp(c):
    return any(in_range(ord(c), range) for range in JP_ID_RANGES)

def is_str_jp(string):
    return any(is_char_jp(c) for c in string)

def grouper(n, iterable):
    it = iter(iterable)
    while True:
       chunk = tuple(itertools.islice(it, n))
       if not chunk:
           return
       yield chunk

class Deepl:
    TRANS_ENDPOINT = 'https://api.deepl.com/v2/translate'

    @dataclass
    class Translation:
        text: str
        detected_source_language: str

    def __init__(self, api_key):
        self.api_key = api_key

    def trans(self, text, src_lang=None, tgt_lang=None, split_sentences=1, preserve_formatting=False, formality=None):
        params = {
            'auth_key': self.api_key,
            'text': text,
            'target_lang': tgt_lang or 'EN-US',
            'split_sentences': split_sentences,
            'preserve_formatting': 1 if preserve_formatting else 0,
            'formality': formality or 'default',
        }
        if src_lang is not None:
            params['src_lang'] = src_lang

        r = requests.get(Deepl.TRANS_ENDPOINT, params=params)
        r.raise_for_status() # don't keep going if there's an error
        resp = r.json()
        translations = r.json()['translations']

        return [Deepl.Translation(**t) for t in translations]

@click.group()
@click.argument('db', type=click.Path())
@click.option('--out', type=click.Path())
@click.pass_context
def cli(ctx, db, out):
    ctx.obj['db_path'] = db
    ctx.obj['out_path'] = out or db # default to overwriting

@cli.command()
@click.pass_context
def passthrough(ctx):
    """pass through non-cjk-containing strings"""

    with click.open_file(ctx.obj['db_path'], 'r') as f:
        db = json.load(f)

    for k, v in db.items():
        # nb: `v is None` makes sure we don't overwrite existing edits
        if v is None and not is_str_jp(k):
            db[k] = k

    with click.open_file(ctx.obj['out_path'], 'w') as f:
        json.dump(db, f, indent=2, ensure_ascii=False)

@cli.command()
@click.option('--api-key', envvar='DEEPL_API_KEY')
@click.pass_context
def deepl(ctx, api_key):
    """translate cjk-containing strings with deepl api.

    sadly, this costs $$$ :(
    """

    with click.open_file(ctx.obj['db_path'], 'r') as f:
        db = json.load(f)

    deepl = Deepl(api_key)


    BATCH_SIZE = 20
    to_translate = [item[0] for item in db.items()
                    if item[1] is None and is_str_jp(item[0])]

    for source_text in tqdm(grouper(BATCH_SIZE, to_translate), total=len(to_translate)//BATCH_SIZE): # max batch size for deepl is 50
        translations = deepl.trans(source_text, src_lang='JA', preserve_formatting=True)
        for original, translation in zip(source_text, translations):
            tqdm.write(f'{original} -> {translation.text}')
            db[original] = translation.text

        # write every time in case we ^C. don't wanna lose progress, deepl is $$$
        with click.open_file(ctx.obj['out_path'], 'w') as f:
            json.dump(db, f, indent=2, ensure_ascii=False)
        time.sleep(0.01)


@cli.command()
@click.pass_context
def stats(ctx):
    with click.open_file(ctx.obj['db_path'], 'r') as f:
        db = json.load(f)

    total = 0
    translated = 0
    jp = 0
    for k, v in db.items():
        total += 1
        if v is not None:
            translated += 1
        if is_str_jp(k):
            jp += 1

    print(f'total: {total}')
    print(f'translated: {translated} ({translated/total * 100:.1f}%)')
    print(f'jp: {jp} ({jp/total * 100:.1f}%)')

@cli.command()
@click.argument('regex')
@click.option('--search', type=click.Choice(['original', 'translation'], case_sensitive=False), default='original')
@click.pass_context
def grep(ctx, regex, search):
    regex = re.compile(regex)

    with click.open_file(ctx.obj['db_path'], 'r') as f:
        db = json.load(f)

    def pred(k, v):
        if search == 'original':
            return regex.search(k) is not None 
        elif search == 'translation':
            return (regex.search(v) is not None) if v is not None else False
    json.dump({k:v for k, v in db.items() if pred(k, v)}, sys.stdout, indent=2, ensure_ascii=False)
    print()

@cli.command()
@click.pass_context
def fixup_dingbats(ctx):
    # ■

    with click.open_file(ctx.obj['db_path'], 'r') as f:
        db = json.load(f)

    r = re.compile(r'([\p{GeometricShapes}\s]+)(.*)([\p{GeometricShapes}\s])')
    for k, v in db.items():
        if v is not None:
            if m := r.match(k):
                if not r.match(v):
                    db[k] = f'{m.group(1)}{v}{m.group(3)}'

    with click.open_file(ctx.obj['out_path'], 'w') as f:
        json.dump(db, f, indent=2, ensure_ascii=False)

if __name__ == '__main__':
    cli(obj={})