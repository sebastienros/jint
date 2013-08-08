/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-11.js
 * @description Array.prototype.lastIndexOf - both array element and search element are Objects, and they refer to the same object
 */


function testcase() {

        var obj1 = {};
        var obj2 = {};
        var obj3 = obj2;
        return [obj2, obj1].lastIndexOf(obj3) === 0;
    }
runTestCase(testcase);
