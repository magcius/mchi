import click
import json
import requests

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
        if not is_str_jp(k):
            db[k] = k

    with click.open_file(ctx.obj['out_path'], 'w') as f:
        json.dump(db, f, indent=2)


if __name__ == '__main__':
    cli(obj={})