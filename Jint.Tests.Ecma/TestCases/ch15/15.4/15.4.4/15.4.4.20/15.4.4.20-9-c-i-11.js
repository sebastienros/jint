/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-11.js
 * @description Array.prototype.filter - element to be retrieved is own accessor property that overrides an inherited data property on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return idx === 0 && val === 11;
        }

        var proto = { 0: 5, 1: 6 };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 10;

        Object.defineProperty(child, "0", {
            get: function () {
                return 11;
            },
            configurable: true
        });
        var newArr = Array.prototype.filter.call(child, callbackfn);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
