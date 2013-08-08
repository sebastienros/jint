/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-a-2.js
 * @description A direct call to arguments.callee.caller should work
 */


function testcase() {
    var called = false;
    
    function test1(flag) {
        if (flag!==true) {
            test2();
        } else {
            called = true;
        }
    }

    function test2() {
        if(arguments.callee.caller===undefined) {
          called=true; // Extension not supported - fake it
        } else {
          arguments.callee.caller(true);
        }
    }
    
    test1();
    return called;   
}

runTestCase(testcase);