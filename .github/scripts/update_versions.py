import os
import json
import re
import xml.etree.ElementTree as ET
from github import Github

def get_latest_release_branch():
    """ GitHub repository'deki tüm `rel-x.x` branch'lerini listeleyerek en büyük sürüme sahip olanı döndürür. """
    g = Github(os.environ["GITHUB_TOKEN"])
    repo = g.get_repo("abpframework/abp")

    branches = repo.get_branches()
    release_branches = []

    # `rel-x.x` look for pattern
    pattern = re.compile(r"rel-(\d+\.\d+)")

    for branch in branches:
        match = pattern.match(branch.name)
        if match:
            release_branches.append((branch.name, float(match.group(1))))  # (branch_name, version)

    if not release_branches:
        raise ValueError("No release branches found!")

    # Find the branch with the highest version
    latest_branch = max(release_branches, key=lambda x: x[1])[0]
    return latest_branch

def get_version_from_common_props(branch):
    """ Belirtilen branch'teki `common.props` dosyasından `Version` ve `LeptonXVersion` bilgilerini çeker. """
    g = Github(os.environ["GITHUB_TOKEN"])
    repo = g.get_repo("abpframework/abp")

    # Take the content of the file
    file_content = repo.get_contents("common.props", ref=branch)
    common_props_content = file_content.decoded_content.decode("utf-8")

    # XML parse it
    root = ET.fromstring(common_props_content)
    version = root.find(".//Version").text
    leptonx_version = root.find(".//LeptonXVersion").text

    return version, leptonx_version

def update_latest_versions():
    latest_branch = get_latest_release_branch()
    version, leptonx_version = get_version_from_common_props(latest_branch)

    if "preview" in version or "rc" in version:
        return False

    with open("latest-versions.json", "r") as f:
        latest_versions = json.load(f)

    new_version_entry = {
        "version": version,
        "releaseDate": "",
        "type": "stable",
        "message": "",
        "leptonx": {
            "version": leptonx_version
        }
    }

    latest_versions.insert(0, new_version_entry)  # Add to the beginning of the list

    with open("latest-versions.json", "w") as f:
        json.dump(latest_versions, f, indent=2)

    return True
