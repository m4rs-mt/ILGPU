"""
generate:
- _data/releases.yml
"""

import fire
import requests
from pathlib import Path
import yaml
import re

BASE_DIR = Path(__file__).parent.parent
OUTPUT_FILE = BASE_DIR / '_data' / 'releases.yml'

URL = "https://api.github.com/repos/m4rs-mt/ILGPU/releases"
ISSUE_URL_TEMPLATE = r"[#\g<id>](https://github.com/m4rs-mt/ILGPU/issues/\g<id>)"  # handle both PRs and issues
USERNAME_URL_TEMPLATE = r"[@\g<username>](https://github.com/\g<username>)"


def create_folder(path: Path) -> None:
    """Create folder if it doesn't exist"""
    if not path.exists():
        path.mkdir(parents=True)


def add_issues(body: str) -> str:
    """Add link to GitHub issues in markdown body"""
    # we can do a cross-check with the API:
    # https://api.github.com/repos/m4rs-mt/ILGPU/issues?filter=all&state=all&per_page=100&page=10
    return re.sub(r'#(?P<id>\d+)', ISSUE_URL_TEMPLATE, body)


def add_users(body: str) -> str:
    """Add link to GitHub users in markdown body"""
    # we can do a cross-check with the API
    return re.sub(r'@(?P<username>[a-zA-Z\d](-?[a-zA-Z\d]+){0,36}[a-zA-Z\d])', USERNAME_URL_TEMPLATE, body)


def lower_markdown_titles(body: str, levels: int) -> str:
    """Change markdown titles level"""
    if levels < 1:
        return body
    level_str = '#' * levels
    return re.sub(r'(?P<prefix>[\^\s\r\n])(?P<level>#+)(?P<suffix>\s+\S+)',
                  rf"\g<prefix>{level_str}\g<level>\g<suffix>", body)


def update_body(body: str) -> str:
    """Update markdown body"""
    body = add_issues(body)
    body = lower_markdown_titles(body, 0)
    body = add_users(body)
    return body


def main():
    response = requests.get(URL)
    response.raise_for_status()
    data = response.json()
    for release in data:
        if release['body']:
            release['body'] = update_body(release['body'])
    create_folder(OUTPUT_FILE.parent)
    with open(OUTPUT_FILE, 'w') as f:
        yaml.dump(data, f)


if __name__ == '__main__':
    fire.Fire(main)
