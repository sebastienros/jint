/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-9.js
 * @description with introduces scope - name lookup finds outer variable
 */


function testcase() {
  function f(o) {
    var x = 42;

    function innerf(o) {
      with (o) {
        return x;
      }
    }

    return innerf(o);
  }
  
  if (f({}) === 42) {
    return true;
  }
 }
runTestCase(testcase);
