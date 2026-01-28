#!/bin/bash

# Generate Unity .meta files for the SDK
# This script creates GUID-based meta files for all assets

generate_guid() {
    # Generate a random GUID
    cat /dev/urandom | tr -dc 'a-f0-9' | fold -w 32 | head -n 1
}

generate_folder_meta() {
    local path="$1"
    local guid=$(generate_guid)
    cat > "${path}.meta" << EOF
fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
EOF
}

generate_cs_meta() {
    local path="$1"
    local guid=$(generate_guid)
    cat > "${path}.meta" << EOF
fileFormatVersion: 2
guid: ${guid}
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
EOF
}

generate_asmdef_meta() {
    local path="$1"
    local guid=$(generate_guid)
    cat > "${path}.meta" << EOF
fileFormatVersion: 2
guid: ${guid}
AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
EOF
}

generate_plugin_meta() {
    local path="$1"
    local guid=$(generate_guid)
    cat > "${path}.meta" << EOF
fileFormatVersion: 2
guid: ${guid}
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
EOF
}

# Process directories
for dir in Runtime Runtime/Configuration Runtime/Capture Runtime/Context Runtime/Transport Runtime/Models Editor Plugins Plugins/iOS Plugins/Android Tests Tests/Runtime Tests/Editor; do
    if [ -d "$dir" ] && [ ! -f "${dir}.meta" ]; then
        echo "Creating meta for folder: $dir"
        generate_folder_meta "$dir"
    fi
done

# Process C# files
find . -name "*.cs" -not -path "./node_modules/*" -not -path "./.git/*" | while read file; do
    if [ ! -f "${file}.meta" ]; then
        echo "Creating meta for: $file"
        generate_cs_meta "$file"
    fi
done

# Process asmdef files
find . -name "*.asmdef" | while read file; do
    if [ ! -f "${file}.meta" ]; then
        echo "Creating meta for: $file"
        generate_asmdef_meta "$file"
    fi
done

# Process plugin files
for ext in java mm h c mk; do
    find ./Plugins -name "*.${ext}" 2>/dev/null | while read file; do
        if [ ! -f "${file}.meta" ]; then
            echo "Creating meta for plugin: $file"
            generate_plugin_meta "$file"
        fi
    done
done

echo "Done generating meta files!"
