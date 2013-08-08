/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-2.js
 * @description Object.isFrozen - 'P' is own data property that overrides an inherited data property
 */


function testcase() {

        var proto = {};

        Object.defineProperty(proto, "foo", {
            value: 9,
            writable: false,
            configurable: false
        });

        var Con = function () { };
        Con.prototype = proto;
        var child = new Con();

        Object.defineProperty(child, "foo", {
            value: 12,
            writable: true,
            configurable: false
        });

        Object.preventExtensions(child);
        return !Object.isFrozen(child);
    }
runTestCase(testcase);
