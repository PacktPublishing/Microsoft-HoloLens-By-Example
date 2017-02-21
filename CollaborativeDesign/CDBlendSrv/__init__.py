bl_info = {
    "name": "Collaborative Design Blender Service (CDBlendSrv)",
    "author": "Joshua Newnham",
    "version": (0, 0, 1),
    "blender": (2, 78, 0),
    "description": "Sync explicitly observed objects across TCP",
    "category": "Object"
}


def install_pip():
    # import sys, os, subprocess
    #
    # raise Exception("install_pip executing: {} {}",
    #       sys.executable, os.path.join(os.path.join(os.path.dirname(__file__),'external', 'pip_installer.py')))
    #
    # subprocess.call(
    #     [
    #         sys.executable,
    #         os.path.join(os.path.join(os.path.dirname(__file__),'external', 'pip_installer.py'))
    #     ]
    # )
    from .pip_installer import main
    main()


def install_zmq():
    print("install zmq")
    import pip
    pip.main(['install', 'zmq'])


def check_depedencies():
    """
    Checks for package depedencies, if not accessible then will try
    to install them.
    """

    print("checking depedencies")

    try:
        import pip
    except ImportError:
        install_pip()

    try:
        import zmq
    except ImportError:
        install_zmq()


def reimport_modules():
    print("reimporting modules")

    from .blender_widgets import reimport_modules
    reimport_modules()


def register():
    check_depedencies()
    reimport_modules()

    from .blender_widgets import register_addon
    register_addon()


def unregister():
    from .blender_widgets import unregister_addon
    unregister_addon()


if __name__ == "__main__":
    #register()
    install_pip()




