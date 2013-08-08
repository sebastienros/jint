/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-2.js
 * @description Object.create - 'Properties' is undefined
 */


function testcase() {

        var newObj = Object.create({}, undefined);
        return (newObj instanceof Object);
    }
runTestCase(testcase);
