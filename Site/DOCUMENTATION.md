# User Guide

This website is built using [Jekyll](https://jekyllrb.com/) and
the [Arsha theme](https://github.com/m4rs-mt/ILGPU.WebTheme).

## Pages

The website contains the following pages:

- [index.html](./index.html): landing page
- [milestones.html](./milestones.html): listing GitHub milestones
- [releases.html](./releases.html): listing GitHub releases
- [404.html](./404.html): not found default page

> in order to customize them, you can edit the files directly.

## News

The website contains a news section, that is being generated dynamically from Markdown and HTML files residing
in [`_posts/`](./_posts) folder.

You can add, remove and update them inside the [`_posts/`](./_posts) folder.

> We recommend using Markdown for the news. HTML is especially useful if we need very specific things that cannot
> be achieved using Markdown.

### Front Matter attributes

Every file should have a [Front Matter](https://jekyllrb.com/docs/front-matter/) header to be processed by Jekyll.
At least, an empty one:

```yaml
---
---
```

We found the following attributes worth mentioning:

```yaml
---
title: YOUR TITLE
permalink: /your-permalink
excerpt: your-short-description
---
```

## Docs section

The documentation section is generated automatically during the build process based on the [`/Docs`](../Docs) folder.

### Rules and naming convention

We recommend using the following rules for the [`/Docs`](../Docs) folder:

- The folder should have a `ReadMe.md` file that will be used as the index.
- Each Markdown file should have a name containing only Letters, Digits and Hyphens.
- To group files, we can use sub-folders.
- To control the order of files/sections, we can prefix the filename/sub-folder with `DD_` where D represents a digit.

> For sections, we can use 1-depth sub-folders only.

## Theming and styling

Please refer to the [theme documentation](https://github.com/m4rs-mt/ILGPU.WebTheme/blob/main/USAGE.md) if
you want to customize/override the theme.

## Build process

During the build process, we generate the following content dynamically:

- `_docs/` folder, based on [`/Docs`](../Docs) folder.
- `_data/releases.yml` from the [GitHub API](https://api.github.com/repos/m4rs-mt/ILGPU/releases).
- `_data/milestones.yml` from the [GitHub API](https://api.github.com/repos/m4rs-mt/ILGPU/milestones).
- `_data/sidebar.yml`: sidebar content.

We also add Front Matter headers to `_docs/` files automatically based on the naming convention.

> We use Python to achieve that, the scripts are located [here](./scripts).

------------------------------------------------------------------------------------------------------------------------

# Developer Guide

This section is useful for developers who are interested in editing the site.

## Getting Started

```shell
cd ./Site
```

### Build

The first step is to generate the `_docs/` and `_data/` folders.

> This step requires Python 3.9+.

```shell
# 1. install requirements
pip install -r scripts/requirements.txt
# 2. generate _data/milestones.yml
python ./scripts/generate_milestones.py
# 3. generate _data/releases.yml
python ./scripts/generate_releases.py
# 4. generate _docs/ + _data/sidebar.yml
python ./scripts/generate_docs.py
```

> This step is required to be able to preview the site locally.

If you want to preview the changes made to [./Docs](../Docs) on the website you will need to
run `python ./scripts/generate_docs.py` each time.

### Install

To install all the dependencies listed in the `Gemfile`, run the following command:

```shell
bundle
# or
bundle install
```

### Run

To preview your site locally, run the Jekyll server:

```shell
# run site locally
bundle exec jekyll serve --config _config.yml,_config.development.yml
# you can clean to remove the generated files
bundle exec jekyll clean
# you can also generate the build locally
bundle exec jekyll build --config _config.yml,_config.development.yml
```

And then open your browser at [`http://localhost:4000`](http://localhost:4000)

For more details you can refer
to [theme documentation](https://github.com/m4rs-mt/ILGPU.WebTheme/blob/main/DEVELOPMENT.md)
