#!/bin/bash
# This script runs commands inside Ubuntu PRoot
COMMAND="$*"  # Takes all arguments as the command

# Execute inside Ubuntu
proot-distro login ubuntu -- bash -c "$COMMAND"