/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-8.js
 * @description Array.prototype.every applied to Array-like object, 'length' is an own accessor property that overrides an inherited data property
 */


function testcase() {
        function callbackfn1(val, idx, obj) {
            return val > 10;
        }

        function callbackfn2(val, idx, obj) {
            return val > 11;
        }

        var proto = { length: 3 };

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

        return Array.prototype.every.call(child, callbackfn1) &&
            !Array.prototype.every.call(child, callbackfn2);
    }
runTestCase(testcase);
