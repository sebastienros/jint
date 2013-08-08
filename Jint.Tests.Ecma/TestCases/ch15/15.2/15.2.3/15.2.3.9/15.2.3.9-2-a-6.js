/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-6.js
 * @description Object.freeze - 'P' is own accessor property that overrides an inherited accessor property
 */


function testcase() {
        var proto = {};

        Object.defineProperty(proto, "foo", {
            get: function () {
                return 0;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        Object.defineProperty(child, "foo", {
            get: function () {
                return 10;
            },
            configurable: true
        });

        Object.freeze(child);

        var desc = Object.getOwnPropertyDescriptor(child, "foo");

        delete child.foo;
        return child.foo === 10 && desc.configurable === false;
    }
runTestCase(testcase);
