mergeInto(LibraryManager.library, {
    ConsoleLog: function (str) {
        var message = UTF8ToString(str);
        eval(message);
    }
});