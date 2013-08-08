/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-3.js
 * @description Object.isFrozen - 'P' is own data property that overrides an inherited accessor property
 */


function testcase() {

        var proto = {};

        Object.defineProperty(proto, "foo", {
            get: function () {
                return 9;
            },
            configurable: false
        });

        var Con = function () { };
        Con.prototype = proto;
        var child = new Con();


        Object.defineProperty(child, "foo", {
            value: 12,
            configurable: true
        });

        Object.preventExtensions(child);
        return !Object.isFrozen(child);
    }
runTestCase(testcase);
