/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-3.js
 * @description Array.prototype.reduceRight - element to be retrieved is own data property that overrides an inherited data property on an Array-like object
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === "20");
            }
        }

        var proto = { 0: 0, 1: 1, 2: 2, length: 2 };
        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child[1] = "10";
        child[2] = "20";
        child.length = 3;

        Array.prototype.reduceRight.call(child, callbackfn);
        return testResult;

    }
runTestCase(testcase);
