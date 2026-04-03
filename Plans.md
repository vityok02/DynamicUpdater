Its planning to have the following folder structure:
src/
  Assemblies/
    Active/...
	Incoming/...

Active: contains currently loaded modules ready to load
Incoming: contains modules contains downloaded modules

If a module was not successfully unloaded, the next loading should be executed without "zombie" assembly
