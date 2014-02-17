OnCheckin Transforms
===========================================
https://OnCheckin.com is a cloud powered continuous deployment service for .Net web developers.

One powerful feature that OnCheckin supports is implicit web.config transforms that are applied at deployment time.

Web.config transforms can be done at multiple levels.

 1. The build type (always web.release.config)
 2. OnCheckin config (web.oncheckin.config)
 3. OnCheckin environment config (web.[environmentname].config)

This Visual Studio extension makes it easy to create both 2 and 3 above by giving you a graphic interface to add bespoke named web.config transforms.

Features
------------------------

__Ability to add named web.config transforms__

By simply right-clicking on your web.config you can action a "Add Environment Transform" and give it a name of your choosing.

__Restructuring manually created transform__

If you've created a web.config transform file manually (maybe called web.production.config or similar) and want it to sit in the heirachy under web.config, this tool will move your web.production.config under web.config as a child solution item, as well as setting all of its build and copy-on-build parameters correctly. This is handy if you want to save opening up your web project's csproj/vbproj file and editing it's properties manually.

Special Thanks
------------------------

Special thanks is offered to the [slow-cheetah](https://github.com/sayedihashimi/slow-cheetah) project by [Sayed Hashimi](http://sedodream.com/). Sayed has offered advice and 