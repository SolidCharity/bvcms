Build on Fedora 30
==================


Preparations:

    dnf install git wget curl make npm
    npm install -g gulp

    # need to install Mono from Microsoft, because it has csc and msbuild
    # see https://www.mono-project.com/download/stable/#download-lin-fedora
    rpm --import "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
    curl https://download.mono-project.com/repo/centos8-stable.repo | tee /etc/yum.repos.d/mono-centos8-stable.repo
    dnf install mono-devel

    # see https://docs.microsoft.com/de-de/nuget/install-nuget-client-tools#macoslinux
    curl -o /usr/local/bin/nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
    alias nuget="mono /usr/local/bin/nuget.exe"
    
    git clone https://github.com/bvcms/bvcms.git
    cd bvcms

    make nuget
    make build
