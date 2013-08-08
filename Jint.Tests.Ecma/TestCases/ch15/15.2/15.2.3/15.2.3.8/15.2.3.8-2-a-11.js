/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-11.js
 * @description Object.seal - 'P' is own property of a Number object that uses Object's [[GetOwnProperty]]
 */


function testcase() {
        var numObj = new Number(-1);

        numObj.foo = 10;
        var preCheck = Object.isExtensible(numObj);
        Object.seal(numObj);

        delete numObj.foo;
        return preCheck && numObj.foo === 10;
    }
runTestCase(testcase);
