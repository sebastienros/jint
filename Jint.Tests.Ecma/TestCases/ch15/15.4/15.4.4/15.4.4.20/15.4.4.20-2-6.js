/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-2-6.js
 * @description Array.prototype.filter applied to Array-like object, 'length' is an inherited data property
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return obj.length === 2;
        }

        var proto = { length: 2 };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child[0] = 12;
        child[1] = 11;
        child[2] = 9;

        var newArr = Array.prototype.filter.call(child, callbackfn);
        return newArr.length === 2;
    }
runTestCase(testcase);
