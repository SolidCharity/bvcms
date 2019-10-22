all: nuget build

build:
	msbuild CmsWeb.sln
	cp /usr/lib/mono/4.5/System.Net.Http.dll CmsWeb/bin
	cp /root/bvcms/packages/Microsoft.AspNet.WebApi.Client.5.2.7/lib/net45/System.Net.Http.Formatting.dll CmsWeb/bin
	mkdir -p CmsWeb/bin/backup
	mv CmsWeb/bin/System.Threading*.dll CmsWeb/bin/backup
	mv CmsWeb/bin/System.Runtime*.dll CmsWeb/bin/backup
	mv CmsWeb/bin/System.IO*.dll CmsWeb/bin/backup
	mv CmsWeb/bin/System.Globalization*.dll CmsWeb/bin/backup

clean:
	git clean -f -d

nuget:
	find . -name packages.config -exec mono /usr/local/bin/nuget.exe install {} -o packages \;

run:
	cd CmsWeb && xsp4 --port 8080

cmsdata:
	msbuild CmsData/CmsData.csproj /p:Configuration=Debug /p:Platform=AnyCPU
