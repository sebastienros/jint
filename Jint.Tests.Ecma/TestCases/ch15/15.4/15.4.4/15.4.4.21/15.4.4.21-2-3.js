/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-3.js
 * @description Array.prototype.reduce - 'length' is an own data property that overrides an inherited data property on an Array-like object
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return (obj.length === 2);
        }

        var proto = { length: 3 };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 2;
        child[0] = 12;
        child[1] = 11;
        child[2] = 9;

        return Array.prototype.reduce.call(child, callbackfn, 1) === true;
    }
runTestCase(testcase);
