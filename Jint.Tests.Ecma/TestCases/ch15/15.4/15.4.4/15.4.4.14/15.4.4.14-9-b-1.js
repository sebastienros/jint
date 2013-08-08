/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-1.js
 * @description Array.prototype.indexOf - non-existent property wouldn't be called
 */


function testcase() {

        return [0, , 2].indexOf(undefined) === -1;
    }
runTestCase(testcase);
