#!/bin/bash

read -p "Salt: " -r salt
read -p "Passcode: " -r -s passcode

# Starting and trailing whitespace cause compatability issues with the C#.
salt="$(echo -ne "$salt" |  sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')"

printf "\n\nRemoteAccessSalt = \"%s\"\nRemoteAccessHash = \"" "$salt"

if hash sha256sum 2> /dev/null; then
	printf "$salt$passcode" | sha256sum | cut -d' ' -f1 | tr -d "\n"
else
	if hash shasum 2> /dev/null; then
		printf "$salt$passcode" | shasum --algorithm 256 | cut -d' ' -f1 | tr -d "\n"
	else
		echo "Could not find a usable SHA256 generator."
		exit 1
	fi
fi

printf "\"\n"
