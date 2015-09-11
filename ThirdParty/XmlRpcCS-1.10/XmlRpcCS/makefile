# Mono/dotGnu make file

all: SampleServer SampleClient SystemObjectClient SampleServerWithExpose GoogleClient

CSFLAGS=-define:__MONO__
#CSC=cscc $(CSFLAGS)
CSC=mcs $(CSFLAGS)

library:
	$(CSC) -target:library -out:XmlRpcCS.dll `find src/nwc/xmlrpc -name \*.cs`

GoogleClient SystemObjectClient SampleServerWithExpose SampleClient SampleServer: library
	$(CSC) -target:exe -out:$@.exe -reference:XmlRpcCS.dll src/sample/$@.cs



clean:
	@-rm *.exe *.pdb *.dll 2> /dev/null

