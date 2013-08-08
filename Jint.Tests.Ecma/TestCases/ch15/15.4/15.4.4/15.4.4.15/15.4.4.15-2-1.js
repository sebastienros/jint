/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-1.js
 * @description Array.prototype.lastIndexOf - 'length' is own data property on an Array-like object
 */


function testcase() {
        var obj = { 1: null, 2: undefined, length: 2 };

        return Array.prototype.lastIndexOf.call(obj, null) === 1 &&
            Array.prototype.lastIndexOf.call(obj, undefined) === -1;
    }
runTestCase(testcase);
