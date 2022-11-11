"""
generate:
- _docs/
- _data/sidebar.yml
"""

import typing as t
import fire
from pathlib import Path
import yaml
import re
from chardet import detect
import shutil
from slugify import slugify

BASE_DIR = Path(__file__).parent.parent
INPUT_DIR = BASE_DIR.parent / 'Docs'
OUTPUT_DIR = BASE_DIR / '_docs'
OUTPUT_FILE = BASE_DIR / '_data' / 'sidebar.yml'

MAIN_PERMALINK = '/docs/'
MAIN_TITLE = 'Documentation'


def prepare_folder(path: Path) -> None:
    """Remove folder if exists then re-create new one."""
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)


def create_folder(path: Path) -> None:
    """Create folder if not exists."""
    if not path.exists():
        path.mkdir(parents=True)


def get_encoding_type(file: Path) -> str:
    """Get encoding type of file."""
    with open(file, 'rb') as f:
        rawdata = f.read()
    return detect(rawdata)['encoding']


def parse_filename(filename: str) -> tuple:
    """Parse filename according to naming convention."""
    result = re.match(r"(?P<order>\d+)[-_]?(?P<stem>.+)", filename)
    if result:
        stem = result.group('stem')
        order = int(result.group('order'))
    else:
        stem = filename
        order = 100
    return order, stem.replace(' ', '-')


def custom_slugify(order: int, stem: str, prefix: bool = True) -> str:
    """return slug."""
    order = str(order).zfill(2) if order != 100 else ''
    stem = slugify(stem)
    if not order:
        return stem
    if prefix:
        return "-".join([order, stem])
    else:
        return "-".join([stem, order])


def get_metadata(file: Path) -> t.Dict:
    """Extract metadata from file path according to naming convention."""
    order, stem = parse_filename(file.stem)
    title = stem.replace('_', ' ').replace('-', ' ').replace(' and ', ' & ')
    gh_path = str(file).replace(f"{INPUT_DIR.parent}/", '')
    if file.parent == INPUT_DIR:  # top level
        output = OUTPUT_DIR / file.name
        section = None
        slug = custom_slugify(order, stem, False)
        permalink = f"{MAIN_PERMALINK}{custom_slugify(order, stem)}/"
    else:
        output = OUTPUT_DIR / str(file).replace(f"{INPUT_DIR}/", '')
        section = file.parent.name
        s_order, s_stem = parse_filename(section)
        slug = custom_slugify(s_order, s_stem, False) + '--' + custom_slugify(order, stem, False)
        permalink = f"{MAIN_PERMALINK}{custom_slugify(s_order, s_stem)}/{custom_slugify(order, stem)}/"
    path = str(output).replace(f"{OUTPUT_DIR.parent}/", '')
    return {
        "stem": stem,
        "order": order,
        "slug": slug,
        "permalink": permalink,
        "title": title,
        "gh_path": gh_path,
        "path": path,
        "section": section,
        "output": output,
        "input": file,
    }


def select_keys(item: t.Dict, keys: t.List) -> t.Dict:
    """Keep only keys in item."""
    return {k: item[k] for k in keys if k in item}


def custom_sort(ll: t.List) -> t.List:
    """Sort list by keys: order, title"""
    return sorted(ll, key=lambda d: (d.get('order', 100), d.get('title', '')))


def section_mapper(x: t.Tuple) -> t.Dict:
    """Helper function to map sections from Dict to List"""
    k, v = x
    s_order, s_stem = parse_filename(k)
    title = s_stem.replace('_', ' ').replace('-', ' ').replace(' and ', ' & ')
    return {
        "order": s_order,
        "title": title,
        "items": v,
    }


def main():
    # building the sidebar
    sidebar = {
        "main": None,
        "sections": {},
        "toplevel": [],
    }
    for file in INPUT_DIR.glob('**/*'):
        if file.suffix in ['.md', '.html', '.markdown']:
            if not sidebar['main'] and file.parent == INPUT_DIR and \
                    file.stem in ['ReadMe', 'README', 'readme', 'Readme']:
                # main file
                main_item = get_metadata(file)
                main_item['slug'] = ''
                main_item['permalink'] = MAIN_PERMALINK
                main_item['title'] = MAIN_TITLE
                sidebar['main'] = main_item
            else:
                my_metadata = get_metadata(file)
                if my_metadata['section']:  # section
                    if my_metadata['section'] in sidebar['sections']:  # section exists
                        sidebar['sections'][my_metadata['section']].append(my_metadata)
                    else:  # section does not exist
                        sidebar['sections'][my_metadata['section']] = [my_metadata]
                else:  # top level
                    sidebar['toplevel'].append(my_metadata)
    # reduce sections
    sidebar['sections'] = custom_sort(list(map(section_mapper, sidebar['sections'].items())))
    for section in sidebar['sections']:
        section['items'] = custom_sort(section['items'])
    sidebar['toplevel'] = custom_sort(sidebar['toplevel'])
    # generating _docs/ folder
    prepare_folder(OUTPUT_DIR)
    all_items = [sidebar['main']]
    all_items.extend(sidebar['toplevel'])
    for section in sidebar['sections']:
        all_items.extend(section['items'])
    for item in all_items:
        create_folder(item['output'].parent)
        with open(item['input'], 'r', encoding=get_encoding_type(item['input'])) as f_in, \
                open(item['output'], 'w') as f_out:
            # front matter header
            front_matter = select_keys(item, ['title', 'gh_path', 'permalink'])  # alternatively we can use: slug
            f_out.write(f"---\n{yaml.dump(front_matter) if front_matter else ''}---\n")
            # content
            content = f_in.read()
            content = content.replace('\r\n', '\n')  # windows line endings
            content = content.replace('`C#', '`c#')  # fix language
            # content = textwrap.fill(content, width=80) # wrap lines
            f_out.write('\n')
            f_out.write(content)
            f_out.write('\n')
    # cleaning sidebar
    sidebar['main'] = select_keys(sidebar['main'], ['path'])
    for section in sidebar['sections']:
        section['items'] = list(map(lambda x: select_keys(x, ['path']), section['items']))
        del section['order']
    sidebar['toplevel'] = list(map(lambda x: select_keys(x, ['path']), sidebar['toplevel']))
    # generating sidebar.yml
    create_folder(OUTPUT_FILE.parent)
    with open(OUTPUT_FILE, 'w') as f_out:
        yaml.dump(sidebar, f_out)


if __name__ == '__main__':
    fire.Fire(main)
