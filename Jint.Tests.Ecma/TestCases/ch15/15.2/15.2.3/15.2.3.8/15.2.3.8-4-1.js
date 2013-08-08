/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-4-1.js
 * @description Object.seal - 'O' is sealed already
 */


function testcase() {

        var obj = {};

        obj.foo = 10; // default value of attributes: writable: true, configurable: true, enumerable: true
        var preCheck = Object.isExtensible(obj);
        Object.seal(obj);

        Object.seal(obj);
        return preCheck && Object.isSealed(obj);

    }
runTestCase(testcase);
