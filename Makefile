prefix = .

all: 
	xbuild b2c/b2c.sln /property:Configuration=Release

install:
	mkdir -p ${prefix}/bin/
	cp b2c/bin/Release/b2c.exe ${prefix}/bin/b2c.exe
	echo "#!/bin/bash\nexec mono ${prefix}/bin/b2c.exe \"$$""@\"" > ${prefix}/bin/b2c
	chmod +x ${prefix}/bin/b2c