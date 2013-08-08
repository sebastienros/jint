/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-7.js
 * @description Object.freeze - 'O' is a RegExp object
 */


function testcase() {
        var regObj = new RegExp();

        Object.freeze(regObj);

        return Object.isFrozen(regObj);
    }
runTestCase(testcase);
