/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.2/11.2.3/11.2.3-3_8.js
 * @description Call arguments are evaluated before the check is made to see if the object is actually callable (global object)
 */


function testcase() {
    if (this!==fnGlobalObject()) {
        return;
    }
    
    var fooCalled = false;
    function foo(){ fooCalled = true; } 
    
    try {
        this.bar( foo() );
        throw new Exception("this.bar does not exist!");
    } catch(e) {
        return (e instanceof TypeError) && (fooCalled===true);
    }
}
runTestCase(testcase);
