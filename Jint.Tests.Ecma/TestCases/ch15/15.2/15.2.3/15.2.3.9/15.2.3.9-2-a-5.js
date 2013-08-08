/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-a-5.js
 * @description Object.freeze - 'P' is own accessor property that overrides an inherited data property
 */


function testcase() {

        var proto = {};

        proto.foo = 0; // default [[Configurable]] attribute value of foo: true

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
