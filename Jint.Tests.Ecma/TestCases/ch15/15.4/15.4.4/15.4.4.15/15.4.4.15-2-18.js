/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-18.js
 * @description Array.prototype.lastIndexOf applied to String object which implements its own property get method
 */


function testcase() {

        var str = new String("012");
        try {
            String.prototype[3] = "3";
            return Array.prototype.lastIndexOf.call(str, "2") === 2 &&
                Array.prototype.lastIndexOf.call(str, "3") === -1;
        } finally {
            delete String.prototype[3];
        }
    }
runTestCase(testcase);
