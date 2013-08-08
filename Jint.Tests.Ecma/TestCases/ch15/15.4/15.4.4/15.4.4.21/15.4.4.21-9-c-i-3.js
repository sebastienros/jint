/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-3.js
 * @description Array.prototype.reduce - element to be retrieved is own data property that overrides an inherited data property on an Array-like object
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === "11");
            }
        }

        var proto = { 0: 0, 1: 1, 2: 2, length: 2 };
        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child[1] = "11";
        child[2] = "22";
        child.length = 3;

        Array.prototype.reduce.call(child, callbackfn, initialValue);
        return testResult;

    }
runTestCase(testcase);
