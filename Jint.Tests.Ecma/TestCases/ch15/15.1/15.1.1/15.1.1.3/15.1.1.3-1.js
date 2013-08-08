/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.1/15.1.1/15.1.1.3/15.1.1.3-1.js
 * @description undefined is not writable, should not throw in non-strict mode
 * @noStrict
 */

function testcase(){
    undefined = 5;
    if(typeof undefined !== "undefined") return false;

    var nosuchproperty;
    if(nosuchproperty !== undefined) return false;
    
    return true;
}

runTestCase(testcase);
