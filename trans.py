import json
import time
from dataclasses import dataclass
import click
from tqdm import tqdm
import requests
from requests.packages.urllib3.util.retry import Retry

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

        return Deepl.Translation(**translations[0])

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
        json.dump(db, f, indent=2)

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

    for k, v in  tqdm([item for item in db.items()
                       if item[1] is None and is_str_jp(item[0])]):
        db[k] = deepl.trans(k, preserve_formatting=True).text
        tqdm.write(f'{k} -> {db[k]}')

        # write every time in case we ^C. don't wanna lose progress, deepl is $$$
        with click.open_file(ctx.obj['out_path'], 'w') as f:
            json.dump(db, f, indent=2)
        time.sleep(0.01)


if __name__ == '__main__':
    cli(obj={})