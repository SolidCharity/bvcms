all: nuget build

build:
	msbuild CmsWeb.sln

nuget:
	find . -name packages.config -exec mono /usr/local/bin/nuget.exe install {} -o packages \;
