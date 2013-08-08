/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-ii-2.js
 * @description Array.prototype.lastIndexOf - both type of array element and type of search element are Undefined
 */


function testcase() {

        return [undefined].lastIndexOf() === 0 && [undefined].lastIndexOf(undefined) === 0;
    }
runTestCase(testcase);
