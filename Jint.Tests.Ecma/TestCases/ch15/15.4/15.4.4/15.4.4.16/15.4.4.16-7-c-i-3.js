/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-3.js
 * @description Array.prototype.every - element to be retrieved is own data property that overrides an inherited data property on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 5) {
                return val === 100;
            } else {
                return true;
            }
        }

        var proto = { 0: 11, 5: 100 };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child[5] = "abc";
        child.length = 10;

        return !Array.prototype.every.call(child, callbackfn);
    }
runTestCase(testcase);
