/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-13.js
 * @description Array.prototype.map - element to be retrieved is own accessor property that overrides an inherited accessor property on an Array-like object
 */


function testcase() {

        var kValue = "abc";

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === kValue;
            }
            return false;
        }

        var proto = { length: 2 };

        Object.defineProperty(proto, "0", {
            get: function () {
                return 5;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        Object.defineProperty(child, "0", {
            get: function () {
                return kValue;
            },
            configurable: true
        });

        var testResult = Array.prototype.map.call(child, callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);
