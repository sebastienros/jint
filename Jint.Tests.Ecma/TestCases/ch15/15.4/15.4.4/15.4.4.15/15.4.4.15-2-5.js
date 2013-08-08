/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-5.js
 * @description Array.prototype.lastIndexOf - 'length' is own data property that overrides an inherited accessor property on an Array-like object
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "length", {
            get: function () {
                return 0;
            },
            configurable: true
        });

        var Con = function () {};
        Con.prototype = proto;

        var child = new Con();

        Object.defineProperty(child, "length", {
            value: 2,
            configurable: true
        });
        child[1] = null;

        return Array.prototype.lastIndexOf.call(child, null) === 1;
    }
runTestCase(testcase);
