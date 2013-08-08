/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-5.js
 * @description Array.prototype.map - applied to Array-like object, 'length' is an own data property that overrides an inherited accessor property
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val > 10;
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
        child.length = 2;
        Object.defineProperty(child, "length", {
            value: 2,
            configurable: true
        });
        child[0] = 12;
        child[1] = 11;
        child[2] = 9;

        var testResult = Array.prototype.map.call(child, callbackfn);

        return testResult.length === 2;
    }
runTestCase(testcase);
