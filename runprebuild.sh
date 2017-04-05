#!/bin/sh

cp lib/* bin/
# mono bin/Prebuild.exe /target nant
mono bin/Prebuild.exe /target vs2015
