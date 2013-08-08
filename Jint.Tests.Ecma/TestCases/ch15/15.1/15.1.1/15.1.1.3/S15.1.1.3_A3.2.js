// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The undefined is DontEnum
 *
 * @path ch15/15.1/15.1.1/15.1.1.3/S15.1.1.3_A3.2.js
 * @description Use for-in statement
 */

// CHECK#1
for (prop in this) {
  if (prop === "undefined") {
	$ERROR('#1: The undefined is DontEnum');
  }	 	
}

