#!/bin/bash
set -e

# Arrays for directories and files that need to be processed
DIRS=("plugins" "module")
FILES=("init.conf" "example.conf")

# Check if we have write permissions in config directory
if [ ! -w /home/config ]; then
    exit 1
fi

# Create base config directory if it doesn't exist
mkdir -p /home/config

# Function to handle directory processing
# Moves directory to config if doesn't exist and creates symlink
handle_directory() {
    local dir=$1

    if [ ! -d "/home/config/$dir" ]; then
        if ! mv "/home/$dir" /home/config/ 2>/dev/null; then
            return 1
        fi
    fi

    # Remove existing directory and create symlink
    rm -rf "/home/$dir"
    if ! ln -s "/home/config/$dir" /home; then
        return 1
    fi
}

# Function to handle file processing
# Moves file to config if doesn't exist and creates symlink
handle_file() {
    local file=$1

    if [ -f "/home/config/$file" ]; then
        rm -f "/home/$file"
    else
        if ! mv "/home/$file" /home/config/ 2>/dev/null; then
            return 1
        fi
    fi

    # Create symlink for the file
    if ! ln -sf "/home/config/$file" /home/; then
        return 1
    fi
}

# Process all directories
for dir in "${DIRS[@]}"; do
    if ! handle_directory "$dir"; then
        exit 1
    fi
done

# Process all files
for file in "${FILES[@]}"; do
    if ! handle_file "$file"; then
        exit 1
    fi
done

exec "$@"
