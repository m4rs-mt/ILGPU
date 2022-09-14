"""
generate:
- _data/milestones.yml
"""

import fire
import requests
from pathlib import Path
import yaml

BASE_DIR = Path(__file__).parent.parent
OUTPUT_FILE = BASE_DIR / '_data' / 'milestones.yml'

URL = "https://api.github.com/repos/m4rs-mt/ILGPU/milestones"


def create_folder(path: Path) -> None:
    """Create folder if it doesn't exist"""
    if not path.exists():
        path.mkdir(parents=True)


def main():
    response = requests.get(URL)
    response.raise_for_status()
    data = response.json()
    create_folder(OUTPUT_FILE.parent)
    with open(OUTPUT_FILE, 'w') as f:
        yaml.dump(data, f)


if __name__ == '__main__':
    fire.Fire(main)
