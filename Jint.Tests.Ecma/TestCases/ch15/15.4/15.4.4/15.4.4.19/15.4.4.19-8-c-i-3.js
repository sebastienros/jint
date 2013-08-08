/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-3.js
 * @description Array.prototype.map - element to be retrieved is own data property that overrides an inherited data property on an Array-like object
 */


function testcase() {

        var kValue = "abc";

        function callbackfn(val, idx, obj) {
            if (idx === 5) {
                return val === kValue;
            }
            return false;
        }

        var proto = { 5: 12, length: 10 };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child[5] = kValue;

        var testResult = Array.prototype.map.call(child, callbackfn);

        return testResult[5] === true;
    }
runTestCase(testcase);
