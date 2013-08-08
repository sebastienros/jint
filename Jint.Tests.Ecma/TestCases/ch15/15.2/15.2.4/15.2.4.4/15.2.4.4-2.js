/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.4/15.2.4.4/15.2.4.4-2.js
 * @description Object.prototype.valueOf - typeof Object.prototype.valueOf.call(false)==="object"
 */


function testcase() {
        return (typeof Object.prototype.valueOf.call(false)) === "object";
}
runTestCase(testcase);