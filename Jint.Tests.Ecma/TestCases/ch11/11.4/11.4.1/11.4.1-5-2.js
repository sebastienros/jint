/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-2.js
 * @description delete operator returns false when deleting a direct reference to a function argument
 */


function testcase() {
  
  function foo(a,b) {
  
    // Now, deleting 'a' directly should fail
    // because 'a' is direct reference to a function argument;
    var d = delete a;
    return (d === false && a === 1);
  }
  return foo(1,2);  
 }
runTestCase(testcase);
