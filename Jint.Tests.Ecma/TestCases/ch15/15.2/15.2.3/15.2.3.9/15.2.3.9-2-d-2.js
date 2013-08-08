/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-2.js
 * @description Object.freeze - 'O' is an Array object
 */


function testcase() {
        var arrObj = [0, 1];

        Object.freeze(arrObj);

        return Object.isFrozen(arrObj);
    }
runTestCase(testcase);
