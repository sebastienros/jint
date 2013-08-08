/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-3.js
 * @description Array.prototype.filter - element to be retrieved is own data property that overrides an inherited data property on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return (idx === 5) && (val === "abc");
        }

        var proto = { 0: 11, 5: 100 };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child[5] = "abc";
        child.length = 10;

        var newArr = Array.prototype.filter.call(child, callbackfn);

        return newArr.length === 1 && newArr[0] === "abc";
    }
runTestCase(testcase);
