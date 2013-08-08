/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-9.js
 * @description Array.prototype.forEach applied to Array-like object, 'length' is an own accessor property that overrides an inherited accessor property
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = (obj.length === 2);
        }

        var proto = {};

        Object.defineProperty(proto, "length", {
            get: function () {
                return 3;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        Object.defineProperty(child, "length", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        child[0] = 12;
        child[1] = 11;
        child[2] = 9;

        Array.prototype.forEach.call(child, callbackfn);
        return result;
    }
runTestCase(testcase);
