#Cinegy NMOS Analyser Tool

Use this tool to view inbound network, RTP and NMOS packet details (work in progress - just does basic RTP details so far)

##How easy is it?

Well, we've added everything you need into a single teeny-tiny EXE again, which just depends on .NET 4. And then we gave it all a nice Apache license, so you can tinker and throw the tool wherever you need to on the planet.

Just run the EXE from inside a command-prompt, and a handy help message will pop up like this:

##Command line arguments:

Double click, or just run without (or with incorrect) arguments, and you'll see this:

```
Cinegy Simple RTP and NMOS monitoring tool v1.0.0 (16/05/2016 13:54:02)

NmosAnalyser 1.0.0.0
Copyright © Cinegy GmbH 2016

ERROR(S):
  -m/--multicastaddress required option is missing.
  -g/--mulicastgroup required option is missing.
  -q, --quiet               (Default: False) Don't print anything to the
                            console
  -m, --multicastaddress    Required. Input multicast address to read from.
  -g, --mulicastgroup       Required. Input multicast group port to read from.
  -l, --logfile             Optional file to record events to.
  -a, --adapter             IP address of the adapter to listen for multicasts
                            (if not set, tries first binding adapter).
  -w, --webservices         (Default: False) Enable Web Services (available on
                            http://localhost:8124/analyser by default).
  -u, --serviceurl          (Default: http://localhost:8124/analyser) Optional
                            service URL for REST web services (must change if
                            running multiple instances with web services
                            enabled.
  --help                    Display this help screen.

Since parameters were not passed at the start, you can now enter the two most important (or just hit enter to quit)

Please enter the multicast address to listen to (e.g. 239.1.1.1):
```

Because most of the time you might want a quick-and-dirty scan of a stream, if you just double-click the EXE (or run without arguments) it will ask you interactively what multicast address and group you want to listen to - perfect for people that hate typing!