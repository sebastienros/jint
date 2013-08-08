/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-2.js
 * @description Object.keys returns the standard built-in Array containing own enumerable properties (function)
 */


function testcase() {
  function foo() {}
  foo.x = 1;
  
  var a = Object.keys(foo);
  if (a.length === 1 &&
      a[0] === 'x') {
    return true;
  }
 }
runTestCase(testcase);
