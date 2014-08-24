baboon2cobertura
================

Translate a <a href="https://github.com/inorton/XR.Baboon">XR.baboon</a> coverage db into a XML expected by cobertura.
Due to some limitations in the baboon report, some information it's listed as static, e.g., branching and complexity.

Installation
------------

Standard way of compile and install from a Makefile.
You can specify a directory prefix to install. The current directory is used a default prefix.

    make
    sudo make install prefix=/usr/local/
    

Usage
-----

    b2c db-file path-to-output sources-path
  
