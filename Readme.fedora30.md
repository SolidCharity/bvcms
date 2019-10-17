Build on Fedora 30
==================


Preparations:

    dnf install git mono-devel mono-winfx wget curl make
    curl https://curl.haxx.se/ca/cacert.pem > cacert.pem && cert-sync cacert.pem
    git clone https://github.com/bvcms/bvcms.git
    cd bvcms
    # see https://docs.microsoft.com/de-de/nuget/install-nuget-client-tools#macoslinux
    curl -o /usr/local/bin/nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
    alias nuget="mono /usr/local/bin/nuget.exe"
    nuget install packages/packages.config -o packages

Build:
    even if we fix the csproj files, somehow the /nostdlib parameter is still part of the call of mcs

    # xbuild UtilityExtensions/UtilityExtensions.csproj
    # xbuild CmsWeb.sln 
    make
