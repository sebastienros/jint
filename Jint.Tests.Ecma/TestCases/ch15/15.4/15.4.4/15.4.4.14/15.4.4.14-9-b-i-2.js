/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-2.js
 * @description Array.prototype.indexOf - element to be retrieved is own data property on an Array
 */


function testcase() {
        return [true, true, true].indexOf(true) === 0 &&
            [false, true, true].indexOf(true) === 1 &&
            [false, false, true].indexOf(true) === 2;
    }
runTestCase(testcase);
