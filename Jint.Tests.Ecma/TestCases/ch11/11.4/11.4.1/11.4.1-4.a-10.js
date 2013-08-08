/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test is actually testing the [[Delete]] internal method (8.12.8). Since the
 * language provides no way to directly exercise [[Delete]], the tests are placed here.
 *
 * @path ch11/11.4/11.4.1/11.4.1-4.a-10.js
 * @description delete operator returns true for property (stringify) defined on built-in object (JSON)
 */


function testcase() {
  try {
      var o = JSON.stringify;
	  var desc;
	  try {
	  	desc = Object.getOwnPropertyDescriptor(JSON, 'stringify')
	  } 
	  catch (e) {
	  };
      var d = delete JSON.stringify;
      if (d === true && JSON.stringify === undefined) {
        return true;
      }
  } finally {
    if (desc) Object.defineProperty(JSON, 'stringify', desc)
	else JSON.stringify = o  /* this branch messes up the attributes */;
  }
 }
runTestCase(testcase);
