all: nuget build

build:
	msbuild CmsWeb.sln

clean:
	git clean -f -d

nuget:
	find . -name packages.config -exec mono /usr/local/bin/nuget.exe install {} -o packages \;
