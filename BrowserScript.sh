#!/bin/sh

operation=$1
OS=$2
shift 2
workingDir="$@"
command="${operation}${OS}"

CheckMacOSX () {
	defaults read "${workingDir}/Firefox/Firefox.app/Contents/Info" CFBundleShortVersionString
}

ExtractMacOSX () {
	hdiutil attach "${workingDir}/Firefox/firefox.dmg"
	cp -R /Volumes/Firefox/Firefox.app "${workingDir}/Firefox/Firefox.app"
	sync
	hdiutil detach /Volumes/Firefox
	sync
	rm "${workingDir}/Firefox/firefox.dmg"
}

RunMacOSX () {
	cd "${workingDir}/Firefox/Firefox.app/Contents/MacOs" || exit
	./firefox -profile "${workingDir}/Firefox/Cookie" -url https://shoujo.tv/authorization.html
}

CheckLinux () {
	grep -oP "(?<=Version=).*" -m 1 "${workingDir}/Firefox/application.ini"
}

ExtractLinux () {
	tar -xjf "${workingDir}/Firefox/firefox.tar.bz2" --strip-components 1
	rm "${workingDir}/Firefox/firefox.tar.bz2"
}

RunLinux () {
	cd "${workingDir}/Firefox" || exit
	firefox -profile "${workingDir}/Firefox/Cookie" -url https://shoujo.tv/authorization.html
}

${command}