/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-7.js
 * @description Array.prototype.some - element to be retrieved is inherited data property on an Array-like object
 */


function testcase() {

        var kValue = 'abc';

        function callbackfn(val, idx, obj) {
            if (5 === idx) {
                return kValue === val;
            }
            return false;
        }

        var proto = { 5: kValue };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 10;

        return Array.prototype.some.call(child, callbackfn);
    }
runTestCase(testcase);
