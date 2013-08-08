/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-5.js
 * @description Object.freeze - 'O' is a Number object
 */


function testcase() {
        var numObj = new Number(3);

        Object.freeze(numObj);

        return Object.isFrozen(numObj);
    }
runTestCase(testcase);
