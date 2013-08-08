/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-5.js
 * @description Object.seal - 'O' is a Number object
 */


function testcase() {

        var numObj = new Number(3);
        var preCheck = Object.isExtensible(numObj);
        Object.seal(numObj);

        return preCheck && Object.isSealed(numObj);

    }
runTestCase(testcase);
